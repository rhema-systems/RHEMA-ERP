'use client';

import { useRef, useState } from 'react';
import {
  CheckCircle2,
  Eye,
  GitCompareArrows,
  History,
  Loader2,
  MoreHorizontal,
  Plus,
  RotateCcw,
  Ruler,
  Send,
  XCircle,
} from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Checkbox } from '@/components/ui/checkbox';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
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
  type ProjectBoqVersionComparisonDto,
  type ProjectBoqVersionDetailDto,
  type ProjectBoqVersionSummaryDto,
  type ProjectBoqVersionWorkspaceDto,
  type ProjectBoqRemeasurementWorkspaceDto,
  type QuantitySurveyBoqVersionType,
} from '@/services/projectService';

type QuantitySurveyBoqVersionActionsProps = {
  projectId: string;
};

type BoqWorkflowAction = 'submit' | 'approve' | 'reject' | 'recall';

const VERSION_LABELS: Record<QuantitySurveyBoqVersionType, string> = {
  Original: 'Original',
  Tender: 'Tender',
  Approved: 'Approved',
  Revised: 'Revised',
  Remeasurement: 'Remeasurement',
  TerminatedRepackaged: 'Terminated / repackaged',
  FinalAccount: 'Final account',
};

const versionLabel = (version: ProjectBoqVersionSummaryDto) =>
  `v${version.versionNumber} · ${VERSION_LABELS[version.versionType]}`;

const formatDateTime = (value: string) =>
  new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));

const formatNumber = (value?: number, maximumFractionDigits = 4) =>
  value == null
    ? '—'
    : value.toLocaleString(undefined, { maximumFractionDigits });

const changeTone = (changeType: string) => {
  switch (changeType) {
    case 'Added':
      return 'border-emerald-200 bg-emerald-50 text-emerald-700';
    case 'Removed':
      return 'border-red-200 bg-red-50 text-red-700';
    case 'Changed':
      return 'border-amber-200 bg-amber-50 text-amber-700';
    default:
      return 'border-slate-200 bg-slate-50 text-slate-600';
  }
};

export function QuantitySurveyBoqVersionActions({
  projectId,
}: QuantitySurveyBoqVersionActionsProps) {
  const { user, hasPermission } = useAuth();
  const canManage = hasPermission('quantity-survey.boq.manage');
  const canApprove = hasPermission('quantity-survey.transactions.approve');
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [workspace, setWorkspace] =
    useState<ProjectBoqVersionWorkspaceDto | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [versionType, setVersionType] =
    useState<QuantitySurveyBoqVersionType>('Original');
  const [sourceVersionId, setSourceVersionId] = useState('none');
  const [changeSummary, setChangeSummary] = useState('');
  const [saving, setSaving] = useState(false);
  const [baselineVersionId, setBaselineVersionId] = useState('');
  const [comparisonVersionId, setComparisonVersionId] = useState('');
  const [comparing, setComparing] = useState(false);
  const [comparison, setComparison] =
    useState<ProjectBoqVersionComparisonDto | null>(null);
  const [detail, setDetail] = useState<ProjectBoqVersionDetailDto | null>(null);
  const [detailLoadingId, setDetailLoadingId] = useState<string | null>(null);
  const [workflowAction, setWorkflowAction] =
    useState<BoqWorkflowAction | null>(null);
  const [workflowVersion, setWorkflowVersion] =
    useState<ProjectBoqVersionSummaryDto | null>(null);
  const [workflowText, setWorkflowText] = useState('');
  const [workflowSaving, setWorkflowSaving] = useState(false);
  const [remeasurementOpen, setRemeasurementOpen] = useState(false);
  const [remeasurementLoading, setRemeasurementLoading] = useState(false);
  const [remeasurementSaving, setRemeasurementSaving] = useState(false);
  const [remeasurementWorkspace, setRemeasurementWorkspace] =
    useState<ProjectBoqRemeasurementWorkspaceDto | null>(null);
  const [remeasurementSelection, setRemeasurementSelection] = useState<
    string[]
  >([]);
  const [remeasurementSummary, setRemeasurementSummary] = useState('');
  const remeasurementRequestIds = useRef(new Map<string, string>());

  const loadWorkspace = async () => {
    setLoading(true);
    try {
      const result =
        await projectService.getProjectBoqVersionWorkspace(projectId);
      setWorkspace(result);
      const newest = result.versions[0];
      const prior = result.versions[1];
      setComparisonVersionId((current) => current || newest?.id || '');
      setBaselineVersionId((current) => current || prior?.id || '');
      setVersionType(result.allowedVersionTypes[0] || 'Original');
      setSourceVersionId(result.currentPublishedVersionId || 'none');
      return result;
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to load BoQ version history.'
      );
      return null;
    } finally {
      setLoading(false);
    }
  };

  const handleOpenChange = (value: boolean) => {
    setOpen(value);
    if (value) void loadWorkspace();
    if (!value) {
      setComparison(null);
      setDetail(null);
    }
  };

  const handleCreateOpen = () => {
    if (!workspace) return;
    setVersionType(workspace.allowedVersionTypes[0] || 'Original');
    setSourceVersionId(workspace.currentPublishedVersionId || 'none');
    setChangeSummary('');
    setCreateOpen(true);
  };

  const openRemeasurement = async () => {
    setRemeasurementOpen(true);
    setRemeasurementLoading(true);
    setRemeasurementSelection([]);
    setRemeasurementSummary('');
    try {
      setRemeasurementWorkspace(
        await projectService.getProjectBoqRemeasurementWorkspace(projectId)
      );
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to load the remeasurement workspace.'
      );
      setRemeasurementWorkspace(null);
    } finally {
      setRemeasurementLoading(false);
    }
  };

  const toggleRemeasurementSheet = (id: string, checked: boolean) =>
    setRemeasurementSelection((current) =>
      checked
        ? current.includes(id)
          ? current
          : [...current, id]
        : current.filter((value) => value !== id)
    );

  const createRemeasurement = async () => {
    if (!remeasurementWorkspace) return;
    const summary = remeasurementSummary.trim();
    if (remeasurementSelection.length === 0 || summary.length < 5) return;
    const selected = [...remeasurementSelection].sort();
    const fingerprint = JSON.stringify({ selected, summary });
    const clientRequestId =
      remeasurementRequestIds.current.get(fingerprint) || crypto.randomUUID();
    remeasurementRequestIds.current.set(fingerprint, clientRequestId);
    setRemeasurementSaving(true);
    try {
      await projectService.createProjectBoqRemeasurement(projectId, {
        clientRequestId,
        measurementSheetIds: selected,
        changeSummary: summary,
      });
      remeasurementRequestIds.current.delete(fingerprint);
      setRemeasurementOpen(false);
      await loadWorkspace();
      toast.success(
        'Remeasurement revision created. Submit it through the existing BoQ approval workflow.'
      );
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to create the governed remeasurement revision.'
      );
    } finally {
      setRemeasurementSaving(false);
    }
  };

  const createSnapshot = async () => {
    if (!workspace) return;
    if (changeSummary.trim().length < 5) {
      toast.error(
        'Enter a meaningful change summary of at least 5 characters.'
      );
      return;
    }
    if (workspace.versions.length > 0 && sourceVersionId === 'none') {
      toast.error('Select the prior version on which this snapshot is based.');
      return;
    }

    setSaving(true);
    try {
      await projectService.createProjectBoqVersion(projectId, {
        versionType,
        sourceVersionId:
          sourceVersionId === 'none' ? undefined : sourceVersionId,
        expectedWorkingSetHash: workspace.workingSetHash,
        changeSummary: changeSummary.trim(),
      });
      const refreshed = await loadWorkspace();
      setCreateOpen(false);
      setComparison(null);
      if (refreshed?.versions[0]) {
        setComparisonVersionId(refreshed.versions[0].id);
        setBaselineVersionId(refreshed.versions[1]?.id || '');
      }
      toast.success('BoQ version snapshot created.');
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to create the BoQ version snapshot.'
      );
    } finally {
      setSaving(false);
    }
  };

  const compareVersions = async () => {
    if (!baselineVersionId || !comparisonVersionId) {
      toast.error('Select a baseline and comparison version.');
      return;
    }
    if (baselineVersionId === comparisonVersionId) {
      toast.error('Select two different BoQ versions.');
      return;
    }

    setComparing(true);
    try {
      setComparison(
        await projectService.compareProjectBoqVersions(
          projectId,
          baselineVersionId,
          comparisonVersionId
        )
      );
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to compare the selected BoQ versions.'
      );
    } finally {
      setComparing(false);
    }
  };

  const viewVersion = async (versionId: string) => {
    setDetailLoadingId(versionId);
    try {
      setDetail(
        await projectService.getProjectBoqVersion(projectId, versionId)
      );
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Unable to load the snapshot.'
      );
    } finally {
      setDetailLoadingId(null);
    }
  };

  const openWorkflowAction = (
    version: ProjectBoqVersionSummaryDto,
    action: BoqWorkflowAction
  ) => {
    setWorkflowVersion(version);
    setWorkflowAction(action);
    setWorkflowText('');
  };

  const closeWorkflowAction = () => {
    if (workflowSaving) return;
    setWorkflowAction(null);
    setWorkflowVersion(null);
    setWorkflowText('');
  };

  const runWorkflowAction = async () => {
    if (!workflowAction || !workflowVersion) return;
    const reasonRequired =
      workflowAction === 'reject' || workflowAction === 'recall';
    if (reasonRequired && workflowText.trim().length < 5) {
      toast.error('Enter a reason of at least 5 characters.');
      return;
    }

    setWorkflowSaving(true);
    try {
      if (workflowAction === 'submit') {
        await projectService.submitProjectBoqVersion(
          projectId,
          workflowVersion.id
        );
      } else if (workflowAction === 'approve') {
        await projectService.approveProjectBoqVersion(
          projectId,
          workflowVersion.id,
          workflowText
        );
      } else if (workflowAction === 'reject') {
        await projectService.rejectProjectBoqVersion(
          projectId,
          workflowVersion.id,
          workflowText
        );
      } else {
        await projectService.recallProjectBoqVersion(
          projectId,
          workflowVersion.id,
          workflowText
        );
      }
      await loadWorkspace();
      toast.success(
        workflowAction === 'submit'
          ? 'BoQ version submitted for approval.'
          : workflowAction === 'approve'
            ? 'BoQ workflow action recorded.'
            : workflowAction === 'reject'
              ? 'BoQ version rejected.'
              : 'BoQ version recalled to draft.'
      );
      setWorkflowAction(null);
      setWorkflowVersion(null);
      setWorkflowText('');
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to complete the BoQ workflow action.'
      );
    } finally {
      setWorkflowSaving(false);
    }
  };

  const workflowTitle =
    workflowAction === 'submit'
      ? 'Submit BoQ for approval'
      : workflowAction === 'approve'
        ? 'Approve BoQ workflow step'
        : workflowAction === 'reject'
          ? 'Reject BoQ version'
          : 'Recall BoQ submission';

  return (
    <>
      <Button
        variant="outline"
        className="gap-2"
        onClick={() => handleOpenChange(true)}
      >
        <History className="h-4 w-4" />
        Versions
      </Button>

      <Dialog open={open} onOpenChange={handleOpenChange}>
        <DialogContent className="max-h-[92vh] max-w-[96vw] overflow-y-auto xl:max-w-6xl">
          <DialogHeader>
            <DialogTitle>BoQ versions and comparison</DialogTitle>
            <DialogDescription>
              Immutable snapshots of the existing project BoQ working lines.
            </DialogDescription>
          </DialogHeader>

          {loading && !workspace ? (
            <div className="flex min-h-48 items-center justify-center">
              <Loader2 className="h-6 w-6 animate-spin text-primary" />
            </div>
          ) : workspace ? (
            <div className="space-y-4">
              <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-muted/30 p-3">
                <div className="text-sm">
                  <span className="font-semibold">
                    {workspace.workingLineCount} working line(s)
                  </span>
                  <span className="ml-2 text-muted-foreground">
                    {workspace.versions.length} saved version(s)
                  </span>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button
                    variant="outline"
                    className="gap-2"
                    onClick={() => void openRemeasurement()}
                    disabled={
                      !canManage || !workspace.currentPublishedVersionId
                    }
                  >
                    <Ruler className="h-4 w-4" />
                    Prepare remeasurement
                  </Button>
                  <Button
                    className="gap-2"
                    onClick={handleCreateOpen}
                    disabled={
                      !canManage ||
                      workspace.workingLineCount === 0 ||
                      workspace.allowedVersionTypes.length === 0
                    }
                  >
                    <Plus className="h-4 w-4" />
                    Create snapshot
                  </Button>
                </div>
              </div>

              <div className="overflow-x-auto rounded-lg border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Version</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Source</TableHead>
                      <TableHead>Lines</TableHead>
                      <TableHead>Created</TableHead>
                      <TableHead>Change summary</TableHead>
                      <TableHead className="w-16" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {workspace.versions.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={7} className="h-24 text-center">
                          No BoQ version snapshots have been created.
                        </TableCell>
                      </TableRow>
                    ) : (
                      workspace.versions.map((version) => {
                        const source = workspace.versions.find(
                          (item) => item.id === version.sourceVersionId
                        );
                        return (
                          <TableRow key={version.id}>
                            <TableCell className="font-medium">
                              {versionLabel(version)}
                            </TableCell>
                            <TableCell>
                              <div className="flex flex-wrap gap-1">
                                <Badge variant="outline">
                                  {version.status}
                                </Badge>
                                {version.isPublished ? (
                                  <Badge className="border-emerald-200 bg-emerald-50 text-emerald-700">
                                    Published
                                  </Badge>
                                ) : null}
                              </div>
                            </TableCell>
                            <TableCell>
                              {source ? versionLabel(source) : '—'}
                            </TableCell>
                            <TableCell>{version.lineCount}</TableCell>
                            <TableCell className="whitespace-nowrap">
                              {formatDateTime(version.snapshotAt)}
                            </TableCell>
                            <TableCell className="max-w-80">
                              <span className="line-clamp-2">
                                {version.changeSummary}
                              </span>
                            </TableCell>
                            <TableCell>
                              <DropdownMenu>
                                <DropdownMenuTrigger asChild>
                                  <Button
                                    variant="ghost"
                                    size="icon"
                                    aria-label={`Actions for ${versionLabel(version)}`}
                                  >
                                    {detailLoadingId === version.id ? (
                                      <Loader2 className="h-4 w-4 animate-spin" />
                                    ) : (
                                      <MoreHorizontal className="h-4 w-4" />
                                    )}
                                  </Button>
                                </DropdownMenuTrigger>
                                <DropdownMenuContent align="end">
                                  <DropdownMenuItem
                                    onClick={() => void viewVersion(version.id)}
                                  >
                                    <Eye className="mr-2 h-4 w-4" />
                                    View immutable snapshot
                                  </DropdownMenuItem>
                                  {canManage &&
                                  (version.status === 'Draft' ||
                                    version.status === 'Rejected') &&
                                  !version.isPublished ? (
                                    <DropdownMenuItem
                                      onClick={() =>
                                        openWorkflowAction(version, 'submit')
                                      }
                                    >
                                      <Send className="mr-2 h-4 w-4" />
                                      Submit for approval
                                    </DropdownMenuItem>
                                  ) : null}
                                  {canApprove &&
                                  version.status === 'PendingApproval' &&
                                  version.canCurrentUserApprove ? (
                                    <>
                                      <DropdownMenuItem
                                        onClick={() =>
                                          openWorkflowAction(version, 'approve')
                                        }
                                      >
                                        <CheckCircle2 className="mr-2 h-4 w-4 text-emerald-600" />
                                        Approve current step
                                      </DropdownMenuItem>
                                      <DropdownMenuItem
                                        className="text-destructive"
                                        onClick={() =>
                                          openWorkflowAction(version, 'reject')
                                        }
                                      >
                                        <XCircle className="mr-2 h-4 w-4" />
                                        Reject
                                      </DropdownMenuItem>
                                    </>
                                  ) : null}
                                  {canManage &&
                                  version.status === 'PendingApproval' &&
                                  version.submittedById === user?.id ? (
                                    <DropdownMenuItem
                                      onClick={() =>
                                        openWorkflowAction(version, 'recall')
                                      }
                                    >
                                      <RotateCcw className="mr-2 h-4 w-4" />
                                      Recall to draft
                                    </DropdownMenuItem>
                                  ) : null}
                                </DropdownMenuContent>
                              </DropdownMenu>
                            </TableCell>
                          </TableRow>
                        );
                      })
                    )}
                  </TableBody>
                </Table>
              </div>

              {workspace.versions.length >= 2 ? (
                <div className="space-y-3 rounded-lg border p-3">
                  <div className="flex flex-wrap items-end gap-3">
                    <div className="min-w-56 flex-1 space-y-1.5">
                      <Label>Baseline version</Label>
                      <Select
                        value={baselineVersionId}
                        onValueChange={setBaselineVersionId}
                      >
                        <SelectTrigger>
                          <SelectValue placeholder="Select baseline" />
                        </SelectTrigger>
                        <SelectContent>
                          {workspace.versions.map((version) => (
                            <SelectItem key={version.id} value={version.id}>
                              {versionLabel(version)}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="min-w-56 flex-1 space-y-1.5">
                      <Label>Comparison version</Label>
                      <Select
                        value={comparisonVersionId}
                        onValueChange={setComparisonVersionId}
                      >
                        <SelectTrigger>
                          <SelectValue placeholder="Select comparison" />
                        </SelectTrigger>
                        <SelectContent>
                          {workspace.versions.map((version) => (
                            <SelectItem key={version.id} value={version.id}>
                              {versionLabel(version)}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <Button
                      className="gap-2"
                      onClick={() => void compareVersions()}
                      disabled={comparing}
                    >
                      {comparing ? (
                        <Loader2 className="h-4 w-4 animate-spin" />
                      ) : (
                        <GitCompareArrows className="h-4 w-4" />
                      )}
                      Compare
                    </Button>
                  </div>

                  {comparison ? (
                    <div className="space-y-3">
                      <div className="flex flex-wrap gap-2 text-xs">
                        <Badge
                          className={changeTone('Added')}
                          variant="outline"
                        >
                          {comparison.addedLineCount} added
                        </Badge>
                        <Badge
                          className={changeTone('Removed')}
                          variant="outline"
                        >
                          {comparison.removedLineCount} removed
                        </Badge>
                        <Badge
                          className={changeTone('Changed')}
                          variant="outline"
                        >
                          {comparison.changedLineCount} changed
                        </Badge>
                        <Badge
                          className={changeTone('Unchanged')}
                          variant="outline"
                        >
                          {comparison.unchangedLineCount} unchanged
                        </Badge>
                        {comparison.currencyTotals.map((total) => (
                          <Badge key={total.currency} variant="secondary">
                            {total.currency} Δ{' '}
                            {formatNumber(total.deltaAmount, 2)}
                          </Badge>
                        ))}
                      </div>
                      <div className="max-h-[42vh] overflow-auto rounded-lg border">
                        <Table>
                          <TableHeader>
                            <TableRow>
                              <TableHead>Change</TableHead>
                              <TableHead>Line</TableHead>
                              <TableHead>Description</TableHead>
                              <TableHead className="text-right">
                                Base qty
                              </TableHead>
                              <TableHead className="text-right">
                                New qty
                              </TableHead>
                              <TableHead className="text-right">
                                Qty Δ
                              </TableHead>
                              <TableHead className="text-right">
                                Rate Δ
                              </TableHead>
                              <TableHead>Fields</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {comparison.lines.map((line) => {
                              const display =
                                line.comparisonLine || line.baselineLine;
                              return (
                                <TableRow key={line.lineKey}>
                                  <TableCell>
                                    <Badge
                                      className={changeTone(line.changeType)}
                                      variant="outline"
                                    >
                                      {line.changeType}
                                    </Badge>
                                  </TableCell>
                                  <TableCell>
                                    {display?.lineNumber ||
                                      display?.itemCode ||
                                      '—'}
                                  </TableCell>
                                  <TableCell className="min-w-64">
                                    {display?.description}
                                  </TableCell>
                                  <TableCell className="text-right">
                                    {formatNumber(line.baselineLine?.quantity)}
                                  </TableCell>
                                  <TableCell className="text-right">
                                    {formatNumber(
                                      line.comparisonLine?.quantity
                                    )}
                                  </TableCell>
                                  <TableCell className="text-right font-medium">
                                    {formatNumber(line.quantityDelta)}
                                  </TableCell>
                                  <TableCell className="text-right">
                                    {formatNumber(line.unitRateDelta)}
                                  </TableCell>
                                  <TableCell className="min-w-48 text-xs text-muted-foreground">
                                    {line.changedFields.join(', ') || '—'}
                                  </TableCell>
                                </TableRow>
                              );
                            })}
                          </TableBody>
                        </Table>
                      </div>
                    </div>
                  ) : null}
                </div>
              ) : null}
            </div>
          ) : null}
        </DialogContent>
      </Dialog>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>Create BoQ version snapshot</DialogTitle>
            <DialogDescription>
              The current working lines are copied into an immutable snapshot.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2">
            <div className="space-y-1.5">
              <Label>Version type</Label>
              <Select
                value={versionType}
                disabled={(workspace?.allowedVersionTypes.length || 0) === 0}
                onValueChange={(value) =>
                  setVersionType(value as QuantitySurveyBoqVersionType)
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(workspace?.allowedVersionTypes || []).map((type) => (
                    <SelectItem key={type} value={type}>
                      {VERSION_LABELS[type]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {workspace?.allowedVersionTypes.length === 0 ? (
                <p className="text-xs text-destructive">
                  No direct snapshot type is permitted by the effective BoQ
                  version policy.
                </p>
              ) : null}
            </div>
            {workspace && workspace.versions.length > 0 ? (
              <div className="space-y-1.5">
                <Label>Based on version</Label>
                <Select
                  value={sourceVersionId}
                  onValueChange={setSourceVersionId}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select prior version" />
                  </SelectTrigger>
                  <SelectContent>
                    {workspace.versions
                      .filter(
                        (version) =>
                          version.id === workspace.currentPublishedVersionId
                      )
                      .map((version) => (
                        <SelectItem key={version.id} value={version.id}>
                          {versionLabel(version)}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
              </div>
            ) : null}
            <div className="space-y-1.5">
              <Label htmlFor="boq-version-summary">Change summary</Label>
              <Textarea
                id="boq-version-summary"
                value={changeSummary}
                onChange={(event) => setChangeSummary(event.target.value)}
                maxLength={2000}
                rows={4}
                placeholder="Describe why this version is being captured."
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setCreateOpen(false)}
              disabled={saving}
            >
              Cancel
            </Button>
            <Button
              onClick={() => void createSnapshot()}
              disabled={
                saving ||
                (workspace?.allowedVersionTypes.length || 0) === 0 ||
                changeSummary.trim().length < 5 ||
                Boolean(
                  workspace?.versions.length && sourceVersionId === 'none'
                )
              }
            >
              {saving ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : null}
              Create snapshot
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={remeasurementOpen} onOpenChange={setRemeasurementOpen}>
        <DialogContent className="max-h-[90vh] max-w-4xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Prepare measurement-derived remeasurement</DialogTitle>
            <DialogDescription>
              Select Recorded sheets from the current approved BoQ. Their
              quantities are grouped by line and routed through the existing BoQ
              approval and publication workflow.
            </DialogDescription>
          </DialogHeader>
          {remeasurementLoading ? (
            <div className="flex min-h-40 items-center justify-center">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : remeasurementWorkspace ? (
            <div className="space-y-4">
              <div className="rounded-lg border bg-muted/30 p-3 text-sm">
                Source: approved BoQ v
                {remeasurementWorkspace.sourceApprovedBoqVersionNumber}
              </div>
              {remeasurementWorkspace.openCandidateVersionId ? (
                <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
                  Complete or resubmit the existing open BoQ candidate before
                  creating another revision.
                </div>
              ) : remeasurementWorkspace.eligibleMeasurements.length === 0 ? (
                <div className="rounded-lg border p-6 text-center text-sm text-muted-foreground">
                  No unused Recorded measurement sheets exist for the current
                  approved BoQ.
                </div>
              ) : (
                <div className="max-h-[44vh] overflow-auto rounded-lg border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead className="w-12" />
                        <TableHead>BoQ line</TableHead>
                        <TableHead>Sheet</TableHead>
                        <TableHead className="text-right">
                          Approved qty
                        </TableHead>
                        <TableHead className="text-right">
                          Measured qty
                        </TableHead>
                        <TableHead>Recorded</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {remeasurementWorkspace.eligibleMeasurements.map(
                        (measurement) => (
                          <TableRow key={measurement.measurementSheetId}>
                            <TableCell>
                              <Checkbox
                                aria-label={`Select ${measurement.sheetReference}`}
                                checked={remeasurementSelection.includes(
                                  measurement.measurementSheetId
                                )}
                                onCheckedChange={(checked) =>
                                  toggleRemeasurementSheet(
                                    measurement.measurementSheetId,
                                    checked === true
                                  )
                                }
                              />
                            </TableCell>
                            <TableCell className="min-w-72">
                              <div className="font-medium">
                                {measurement.boqLineLabel}
                              </div>
                              <div className="text-xs text-muted-foreground">
                                {measurement.unitOfMeasure || 'No unit'}
                              </div>
                            </TableCell>
                            <TableCell>{measurement.sheetReference}</TableCell>
                            <TableCell className="text-right">
                              {formatNumber(measurement.previousQuantity)}
                            </TableCell>
                            <TableCell className="text-right font-medium">
                              {formatNumber(measurement.measuredQuantity)}
                            </TableCell>
                            <TableCell className="whitespace-nowrap text-xs">
                              {formatDateTime(measurement.recordedAt)}
                            </TableCell>
                          </TableRow>
                        )
                      )}
                    </TableBody>
                  </Table>
                </div>
              )}
              <div className="space-y-1.5">
                <Label htmlFor="remeasurement-summary">Change summary</Label>
                <Textarea
                  id="remeasurement-summary"
                  value={remeasurementSummary}
                  onChange={(event) =>
                    setRemeasurementSummary(event.target.value)
                  }
                  rows={3}
                  maxLength={2000}
                  placeholder="Explain the measured quantity revision."
                />
              </div>
              <p className="text-xs text-muted-foreground">
                {remeasurementSelection.length} sheet(s) selected. Quantities
                are calculated on the server; approval updates the working BoQ
                only after the shared workflow succeeds.
              </p>
            </div>
          ) : null}
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setRemeasurementOpen(false)}
              disabled={remeasurementSaving}
            >
              Cancel
            </Button>
            <Button
              onClick={() => void createRemeasurement()}
              disabled={
                remeasurementSaving ||
                !remeasurementWorkspace ||
                Boolean(remeasurementWorkspace.openCandidateVersionId) ||
                remeasurementSelection.length === 0 ||
                remeasurementSummary.trim().length < 5
              }
            >
              {remeasurementSaving ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : null}
              Create remeasurement revision
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(workflowAction && workflowVersion)}
        onOpenChange={(value) => !value && closeWorkflowAction()}
      >
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{workflowTitle}</DialogTitle>
            <DialogDescription>
              {workflowVersion
                ? `${versionLabel(workflowVersion)} · ${workflowVersion.changeSummary}`
                : 'Complete the shared approval workflow action.'}
            </DialogDescription>
          </DialogHeader>
          {workflowAction === 'submit' ? (
            <p className="text-sm text-muted-foreground">
              The immutable snapshot hash will be revalidated and routed through
              the workflow selected by the effective BoQ policy.
            </p>
          ) : (
            <div className="space-y-1.5">
              <Label htmlFor="boq-workflow-comment">
                {workflowAction === 'approve'
                  ? 'Approval comment (optional)'
                  : 'Reason'}
              </Label>
              <Textarea
                id="boq-workflow-comment"
                value={workflowText}
                onChange={(event) => setWorkflowText(event.target.value)}
                maxLength={2000}
                rows={4}
                placeholder={
                  workflowAction === 'approve'
                    ? 'Add an approval comment if needed.'
                    : 'Enter the controlled reason for this action.'
                }
              />
            </div>
          )}
          <DialogFooter>
            <Button
              variant="outline"
              onClick={closeWorkflowAction}
              disabled={workflowSaving}
            >
              Cancel
            </Button>
            <Button
              variant={workflowAction === 'reject' ? 'destructive' : 'default'}
              onClick={() => void runWorkflowAction()}
              disabled={
                workflowSaving ||
                ((workflowAction === 'reject' || workflowAction === 'recall') &&
                  workflowText.trim().length < 5)
              }
            >
              {workflowSaving ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : null}
              {workflowAction === 'submit'
                ? 'Submit'
                : workflowAction === 'approve'
                  ? 'Approve'
                  : workflowAction === 'reject'
                    ? 'Reject'
                    : 'Recall'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(detail)}
        onOpenChange={(value) => !value && setDetail(null)}
      >
        <DialogContent className="max-h-[92vh] max-w-[96vw] overflow-y-auto xl:max-w-6xl">
          <DialogHeader>
            <DialogTitle>
              {detail ? versionLabel(detail) : 'BoQ version'}
            </DialogTitle>
            <DialogDescription>
              {detail?.changeSummary || 'Immutable BoQ snapshot lines.'}
            </DialogDescription>
          </DialogHeader>
          {detail ? (
            <div className="max-h-[70vh] overflow-auto rounded-lg border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Line</TableHead>
                    <TableHead>Package</TableHead>
                    <TableHead>Description</TableHead>
                    <TableHead>Classification</TableHead>
                    <TableHead className="text-right">Quantity</TableHead>
                    <TableHead>Unit</TableHead>
                    <TableHead className="text-right">Rate</TableHead>
                    <TableHead className="text-right">Amount</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {detail.lines.map((line) => (
                    <TableRow key={line.id}>
                      <TableCell>
                        {line.lineNumber || line.itemCode || '—'}
                      </TableCell>
                      <TableCell>
                        <div>{line.packageCode || line.packageName || '—'}</div>
                        {line.activityTitle ? (
                          <div className="mt-1 text-xs text-muted-foreground">
                            {line.activityNodeType || 'Activity'} ·{' '}
                            {line.activityTitle}
                          </div>
                        ) : null}
                      </TableCell>
                      <TableCell className="min-w-64">
                        {line.description}
                      </TableCell>
                      <TableCell className="min-w-48 text-xs text-muted-foreground">
                        {[
                          line.sectionCode,
                          line.tradeCode,
                          line.costCode,
                          line.measurementCode,
                        ]
                          .filter(Boolean)
                          .join(' · ') || '—'}
                      </TableCell>
                      <TableCell className="text-right">
                        {formatNumber(line.quantity)}
                      </TableCell>
                      <TableCell>{line.unitOfMeasure || '—'}</TableCell>
                      <TableCell className="text-right">
                        {formatNumber(line.unitRate)}
                      </TableCell>
                      <TableCell className="text-right">
                        {line.currency} {formatNumber(line.lineAmount, 2)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          ) : null}
        </DialogContent>
      </Dialog>
    </>
  );
}
