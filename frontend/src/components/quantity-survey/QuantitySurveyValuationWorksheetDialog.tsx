'use client';

import React, { useCallback, useMemo, useRef, useState } from 'react';
import {
  Calculator,
  CheckCircle2,
  Download,
  History,
  Save,
  Send,
  Upload,
  XCircle,
} from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
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
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import {
  quantitySurveyValuationWorksheetService as service,
  type ValuationWorksheet,
  type ValuationWorksheetLine,
  type ValuationWorksheetLookups,
  type ValuationWorksheetRevision,
} from '@/services/quantity-survey-valuation-worksheet.service';

type Props = {
  projectId: string;
  interimValuationId: string;
  interimValuationStatus: string;
  currency: string;
};

const money = (value: number, currency: string) =>
  new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
    maximumFractionDigits: 2,
  }).format(value || 0);
const quantity = (value: number) =>
  Number(value || 0).toLocaleString(undefined, { maximumFractionDigits: 4 });
const numberValue = (value: string) => (value === '' ? 0 : Number(value));

export function QuantitySurveyValuationWorksheetDialog({
  projectId,
  interimValuationId,
  interimValuationStatus,
  currency,
}: Props) {
  const { hasPermission } = useAuth();
  const canRead = hasPermission('quantity-survey.workspace.read');
  const hasManage = hasPermission('quantity-survey.valuations.manage');
  const canApprove = hasPermission('quantity-survey.transactions.approve');
  const canAudit = hasPermission('quantity-survey.audit.read');
  const request = useRef<{ fingerprint: string; id: string } | undefined>(
    undefined
  );
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [lookups, setLookups] = useState<ValuationWorksheetLookups>({
    approvedBoqVersions: [],
    contractors: [],
    consultants: [],
  });
  const [boqVersionId, setBoqVersionId] = useState('');
  const [worksheet, setWorksheet] = useState<ValuationWorksheet>();
  const [lines, setLines] = useState<ValuationWorksheetLine[]>([]);
  const [retentionPercentage, setRetentionPercentage] = useState(0);
  const [contractorBusinessPartnerId, setContractorBusinessPartnerId] =
    useState('');
  const [consultantBusinessPartnerId, setConsultantBusinessPartnerId] =
    useState('');
  const [history, setHistory] = useState<ValuationWorksheetRevision[]>([]);
  const [reason, setReason] = useState('');
  const [evidenceTitle, setEvidenceTitle] = useState('');
  const [evidenceType, setEvidenceType] = useState<
    | 'ContractorClaim'
    | 'MeasurementSupport'
    | 'SiteRecord'
    | 'ConsultantReview'
    | 'SupportingDocument'
  >('SupportingDocument');
  const [evidenceFile, setEvidenceFile] = useState<File | null>(null);

  const loadWorksheet = useCallback(
    async (selectedVersionId?: string) => {
      setLoading(true);
      try {
        const lookupResult = await service.lookups(projectId);
        setLookups(lookupResult);
        const selected =
          selectedVersionId || lookupResult.approvedBoqVersions[0]?.id;
        if (!selected) {
          setWorksheet(undefined);
          setLines([]);
          toast.error(
            'Publish an approved project BoQ before preparing a valuation worksheet.'
          );
          return;
        }
        const result = await service.get(interimValuationId, selected);
        setBoqVersionId(result.projectBoqVersionId);
        setWorksheet(result);
        setLines(result.lines);
        setRetentionPercentage(result.retentionPercentage);
        setContractorBusinessPartnerId(
          result.contractorBusinessPartnerId || ''
        );
        setConsultantBusinessPartnerId(
          result.consultantBusinessPartnerId || ''
        );
        setHistory([]);
      } catch (error) {
        toast.error(
          error instanceof Error
            ? error.message
            : 'Failed to load valuation worksheet'
        );
      } finally {
        setLoading(false);
      }
    },
    [interimValuationId, projectId]
  );
  const canManage =
    hasManage &&
    Boolean(worksheet) &&
    ['Draft', 'ContractorSubmitted', 'UnderQsReview'].includes(
      worksheet?.status || ''
    ) &&
    ['Draft', 'Submitted', 'UnderReview'].includes(interimValuationStatus);
  const canEditClaim = canManage && worksheet?.status === 'Draft';

  const derived = useMemo(() => {
    const rate = Math.max(0, retentionPercentage) / 100;
    return lines.map((line) => {
      const claimed = line.currentClaimedQuantity || 0;
      const certified = line.currentCertifiedQuantity || 0;
      const claimedValue = claimed * line.unitRate;
      const certifiedValue = certified * line.unitRate;
      const current = certifiedValue - line.previouslyCertifiedValue;
      const retentionToDate = certifiedValue * rate;
      const currentRetention = retentionToDate - line.previousRetentionValue;
      return {
        ...line,
        disputedQuantity: claimed - certified,
        currentClaimedValue: claimedValue,
        currentCertifiedValue: certifiedValue,
        currentPeriodCertifiedValue: current,
        disputedValue: claimedValue - certifiedValue,
        retentionToDateValue: retentionToDate,
        currentRetentionValue: currentRetention,
        netCurrentValue: current - currentRetention,
      };
    });
  }, [lines, retentionPercentage]);
  const totals = useMemo(
    () =>
      derived.reduce(
        (result, line) => ({
          measured: result.measured + line.measuredToDateValue,
          previous: result.previous + line.previouslyCertifiedValue,
          claimed: result.claimed + line.currentClaimedValue,
          certified: result.certified + line.currentCertifiedValue,
          disputed: result.disputed + line.disputedValue,
          retention: result.retention + line.currentRetentionValue,
          net: result.net + line.netCurrentValue,
        }),
        {
          measured: 0,
          previous: 0,
          claimed: 0,
          certified: 0,
          disputed: 0,
          retention: 0,
          net: 0,
        }
      ),
    [derived]
  );

  const save = async () => {
    if (!worksheet) return;
    const payload = {
      projectBoqVersionId: boqVersionId,
      contractorBusinessPartnerId: contractorBusinessPartnerId || null,
      consultantBusinessPartnerId: consultantBusinessPartnerId || null,
      rowVersion: worksheet.rowVersion,
      retentionPercentage,
      lines: lines.map((line) => ({
        projectBoqVersionLineId: line.projectBoqVersionLineId,
        currentClaimedQuantity: line.currentClaimedQuantity,
        currentCertifiedQuantity: line.currentCertifiedQuantity,
        reviewNote: line.reviewNote,
      })),
    };
    const fingerprint = JSON.stringify(payload);
    if (request.current?.fingerprint !== fingerprint)
      request.current = { fingerprint, id: crypto.randomUUID() };
    setSaving(true);
    setActionError(null);
    try {
      const saved = await service.save(interimValuationId, {
        clientRequestId: request.current.id,
        ...payload,
      });
      setWorksheet(saved);
      setLines(saved.lines);
      setRetentionPercentage(saved.retentionPercentage);
      request.current = undefined;
      toast.success('Valuation worksheet saved and reconciled.');
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to save valuation worksheet';
      setActionError(message);
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  const lifecycle = async (
    action: 'vet' | 'submitApproval' | 'approve' | 'reject'
  ) => {
    if (!worksheet?.id || !worksheet.rowVersion || reason.trim().length < 5) {
      toast.error('Enter a reason of at least 5 characters.');
      return;
    }
    setSaving(true);
    setActionError(null);
    try {
      const updated = await service[action](worksheet.id, {
        clientRequestId: crypto.randomUUID(),
        rowVersion: worksheet.rowVersion,
        reason: reason.trim(),
      });
      setWorksheet(updated);
      setLines(updated.lines);
      setReason('');
      toast.success(
        action === 'vet'
          ? 'Valuation vetted.'
          : action === 'submitApproval'
            ? 'Valuation submitted for approval.'
            : action === 'approve'
              ? updated.certificateReady
                ? 'Valuation approved and certificate-ready.'
                : 'Review recorded. The valuation is awaiting the next approval step.'
              : 'Valuation rejected.'
      );
    } catch (error) {
      const message = error instanceof Error ? error.message : 'The valuation action failed';
      setActionError(message);
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  const addEvidence = async () => {
    if (!worksheet?.id || !evidenceFile || evidenceTitle.trim().length < 3) {
      toast.error('Select a file and enter an evidence title.');
      return;
    }
    setSaving(true);
    setActionError(null);
    try {
      await service.addEvidence(projectId, worksheet.id, {
        clientRequestId: crypto.randomUUID(),
        evidenceType,
        title: evidenceTitle.trim(),
        file: evidenceFile,
      });
      setEvidenceFile(null);
      setEvidenceTitle('');
      await loadWorksheet(boqVersionId);
      toast.success('Evidence scanned and retained in the central DMS.');
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Evidence upload failed';
      setActionError(message);
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  const openEvidence = async (evidenceId: string, fileName: string) => {
    if (!worksheet?.id) return;
    try {
      const blob = await service.evidenceContent(
        projectId,
        worksheet.id,
        evidenceId
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Evidence could not be opened'
      );
    }
  };

  const loadHistory = async () => {
    try {
      setHistory(await service.history(interimValuationId));
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Failed to load worksheet history'
      );
    }
  };
  const updateLine = (id: string, change: Partial<ValuationWorksheetLine>) =>
    setLines((current) =>
      current.map((line) =>
        line.projectBoqVersionLineId === id ? { ...line, ...change } : line
      )
    );

  if (!canRead) return null;
  return (
    <Dialog
      open={open}
      onOpenChange={(value) => {
        setOpen(value);
        if (value) void loadWorksheet();
      }}
    >
      <DialogTrigger asChild>
        <Button variant="outline" size="sm">
          <Calculator className="mr-2 h-4 w-4" />
          Worksheet
        </Button>
      </DialogTrigger>
      <DialogContent className="flex max-h-[92vh] max-w-[96vw] flex-col overflow-hidden xl:max-w-[1500px]">
        <DialogHeader>
          <DialogTitle>Line valuation worksheet</DialogTitle>
          <DialogDescription>Review measured quantities, supporting evidence and the current valuation before submitting for approval.</DialogDescription>
        </DialogHeader>
        {actionError && <div role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">{actionError}</div>}
        {loading ? (
          <div className="p-8 text-center text-sm text-muted-foreground">
            Loading recorded measurements and prior certificates…
          </div>
        ) : !worksheet ? (
          <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">
            No approved published BoQ is available.
          </div>
        ) : (
          <>
            <div className="grid gap-3 rounded-lg border bg-muted/20 p-3 md:grid-cols-2 xl:grid-cols-[minmax(240px,1fr)_minmax(200px,0.8fr)_minmax(200px,0.8fr)_140px_auto] xl:items-end">
              <div className="grid gap-1">
                <Label>Approved BoQ version</Label>
                <Select
                  value={boqVersionId}
                  disabled={Boolean(worksheet.id)}
                  onValueChange={(value) => {
                    setBoqVersionId(value);
                    void loadWorksheet(value);
                  }}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {lookups.approvedBoqVersions.map((item) => (
                      <SelectItem key={item.id} value={item.id}>
                        {item.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-1">
                <Label>Contractor</Label>
                <Select
                  value={contractorBusinessPartnerId || 'none'}
                  disabled={
                    !canEditClaim || Boolean(worksheet.contractorSubmittedAt)
                  }
                  onValueChange={(value) =>
                    setContractorBusinessPartnerId(
                      value === 'none' ? '' : value
                    )
                  }
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select contractor" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Not required</SelectItem>
                    {lookups.contractors.map((item) => (
                      <SelectItem key={item.id} value={item.id}>
                        {item.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-1">
                <Label>Consultant</Label>
                <Select
                  value={consultantBusinessPartnerId || 'none'}
                  disabled={
                    !canEditClaim || Boolean(worksheet.contractorSubmittedAt)
                  }
                  onValueChange={(value) =>
                    setConsultantBusinessPartnerId(
                      value === 'none' ? '' : value
                    )
                  }
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select consultant" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Not required</SelectItem>
                    {lookups.consultants.map((item) => (
                      <SelectItem key={item.id} value={item.id}>
                        {item.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-1">
                <Label>Retention %</Label>
                <Input
                  type="number"
                  min={0}
                  max={100}
                  step="0.01"
                  disabled={!canManage}
                  value={retentionPercentage}
                  onChange={(event) =>
                    setRetentionPercentage(numberValue(event.target.value))
                  }
                />
              </div>
              <div className="flex gap-2">
                {canAudit && worksheet.id ? (
                  <Button type="button" variant="outline" onClick={loadHistory}>
                    <History className="mr-2 h-4 w-4" />
                    History
                  </Button>
                ) : null}
                <Button
                  type="button"
                  disabled={!canManage || saving}
                  onClick={save}
                >
                  <Save className="mr-2 h-4 w-4" />
                  {saving ? 'Saving…' : 'Save worksheet'}
                </Button>
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Badge>{worksheet.status}</Badge>
              <Badge variant="outline">{worksheet.approvalStatus}</Badge>
              {worksheet.contractorName ? (
                <span className="text-xs text-muted-foreground">
                  Contractor: {worksheet.contractorName}
                </span>
              ) : null}
              {worksheet.consultantName ? (
                <span className="text-xs text-muted-foreground">
                  Consultant: {worksheet.consultantName}
                </span>
              ) : null}
              {worksheet.certificateReady ? (
                <Badge className="bg-emerald-600">Certificate ready</Badge>
              ) : null}
            </div>
            <div className="grid grid-cols-2 gap-2 md:grid-cols-4 xl:grid-cols-7">
              {[
                ['Measured', totals.measured],
                ['Previous', totals.previous],
                ['Claimed', totals.claimed],
                ['Certified', totals.certified],
                ['Disputed', totals.disputed],
                ['Current retention', totals.retention],
                ['Net current', totals.net],
              ].map(([label, value]) => (
                <div key={String(label)} className="rounded-lg border p-2">
                  <div className="text-xs text-muted-foreground">{label}</div>
                  <div className="font-semibold">
                    {money(Number(value), currency)}
                  </div>
                </div>
              ))}
            </div>
            <div className="min-h-0 flex-1 overflow-auto rounded-lg border">
              <table className="min-w-[1480px] text-sm">
                <thead className="sticky top-0 z-10 bg-muted">
                  <tr>
                    {[
                      'BoQ line',
                      'BoQ qty',
                      'Rate',
                      'Measured to date',
                      'Previously certified',
                      'Current claimed',
                      'Current certified',
                      'Disputed',
                      'Current value',
                      'Retention',
                      'Net current',
                      'Review note',
                    ].map((label) => (
                      <th
                        key={label}
                        className="whitespace-nowrap px-3 py-2 text-left text-xs font-medium"
                      >
                        {label}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {derived.map((line) => (
                    <tr
                      key={line.projectBoqVersionLineId}
                      className="border-t align-top"
                    >
                      <td className="max-w-[260px] px-3 py-2">
                        <div className="font-medium">{line.label}</div>
                        <div
                          className="truncate text-xs text-muted-foreground"
                          title={line.description}
                        >
                          {line.description}
                        </div>
                      </td>
                      <td className="px-3 py-2">
                        {quantity(line.boqQuantity)} {line.unitOfMeasure}
                      </td>
                      <td className="px-3 py-2">
                        {money(line.unitRate, currency)}
                      </td>
                      <td className="px-3 py-2 font-medium">
                        {quantity(line.measuredToDateQuantity)}
                      </td>
                      <td className="px-3 py-2">
                        {quantity(line.previouslyCertifiedQuantity)}
                      </td>
                      <td className="w-36 px-3 py-2">
                        <Input
                          aria-label={`Current claimed ${line.label}`}
                          type="number"
                          min={line.previouslyCertifiedQuantity}
                          max={line.measuredToDateQuantity}
                          step="0.0001"
                          disabled={!canEditClaim}
                          value={line.currentClaimedQuantity}
                          onChange={(event) =>
                            updateLine(line.projectBoqVersionLineId, {
                              currentClaimedQuantity: numberValue(
                                event.target.value
                              ),
                            })
                          }
                        />
                      </td>
                      <td className="w-36 px-3 py-2">
                        <Input
                          aria-label={`Current certified ${line.label}`}
                          type="number"
                          min={line.previouslyCertifiedQuantity}
                          max={line.currentClaimedQuantity}
                          step="0.0001"
                          disabled={!canManage}
                          value={line.currentCertifiedQuantity}
                          onChange={(event) =>
                            updateLine(line.projectBoqVersionLineId, {
                              currentCertifiedQuantity: numberValue(
                                event.target.value
                              ),
                            })
                          }
                        />
                      </td>
                      <td className="px-3 py-2">
                        {quantity(line.disputedQuantity)}
                      </td>
                      <td className="px-3 py-2">
                        {money(line.currentPeriodCertifiedValue, currency)}
                      </td>
                      <td className="px-3 py-2">
                        {money(line.currentRetentionValue, currency)}
                      </td>
                      <td className="px-3 py-2 font-medium">
                        {money(line.netCurrentValue, currency)}
                      </td>
                      <td className="w-64 px-3 py-2">
                        <Textarea
                          aria-label={`Review note ${line.label}`}
                          rows={2}
                          disabled={!canManage}
                          placeholder={
                            line.disputedQuantity > 0
                              ? 'Required for disputed quantity'
                              : 'Optional'
                          }
                          value={line.reviewNote ?? ''}
                          onChange={(event) =>
                            updateLine(line.projectBoqVersionLineId, {
                              reviewNote: event.target.value,
                            })
                          }
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {worksheet.id &&
            !['PendingApproval', 'Approved', 'Rejected'].includes(
              worksheet.status
            ) ? (
              <div className="grid gap-3 rounded-lg border p-3 lg:grid-cols-[180px_minmax(180px,1fr)_minmax(220px,1fr)_auto] lg:items-end">
                <div className="grid gap-1">
                  <Label>Evidence type</Label>
                  <Select
                    value={evidenceType}
                    onValueChange={(value) =>
                      setEvidenceType(value as typeof evidenceType)
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {[
                        'ContractorClaim',
                        'MeasurementSupport',
                        'SiteRecord',
                        'ConsultantReview',
                        'SupportingDocument',
                      ].map((item) => (
                        <SelectItem key={item} value={item}>
                          {item.replace(/([a-z])([A-Z])/g, '$1 $2')}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-1">
                  <Label>Evidence title</Label>
                  <Input
                    value={evidenceTitle}
                    onChange={(event) => setEvidenceTitle(event.target.value)}
                  />
                </div>
                <div className="grid gap-1">
                  <Label>File</Label>
                  <Input
                    type="file"
                    onChange={(event) =>
                      setEvidenceFile(event.target.files?.[0] || null)
                    }
                  />
                </div>
                <Button
                  type="button"
                  variant="outline"
                  disabled={!evidenceFile || saving}
                  onClick={addEvidence}
                >
                  <Upload className="mr-2 h-4 w-4" />
                  Add evidence
                </Button>
              </div>
            ) : null}
            {worksheet.evidence.length ? (
              <div className="flex flex-wrap gap-2">
                {worksheet.evidence.map((item) => (
                  <Button
                    key={item.id}
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => openEvidence(item.id, item.originalFileName)}
                  >
                    <Download className="mr-2 h-3.5 w-3.5" />
                    {item.title}
                  </Button>
                ))}
              </div>
            ) : null}
            {worksheet.id ? (
              <div className="flex flex-wrap items-end gap-2 rounded-lg border p-3">
                <div className="grid min-w-[280px] flex-1 gap-1">
                  <Label>Lifecycle reason</Label>
                  <Input
                    value={reason}
                    onChange={(event) => setReason(event.target.value)}
                    placeholder="Required for vetting and approval decisions"
                  />
                </div>
                {hasManage &&
                ((worksheet.status === 'Draft' &&
                  !worksheet.contractorSubmissionRequired) ||
                  ['ContractorSubmitted', 'UnderQsReview'].includes(
                    worksheet.status
                  )) ? (
                  <Button
                    type="button"
                    variant="outline"
                    disabled={saving}
                    onClick={() => lifecycle('vet')}
                  >
                    <CheckCircle2 className="mr-2 h-4 w-4" />
                    Vet valuation
                  </Button>
                ) : null}
                {hasManage &&
                ((worksheet.status === 'QsVetted' &&
                  !worksheet.consultantEndorsementRequired) ||
                  worksheet.status === 'ConsultantEndorsed') ? (
                  <Button
                    type="button"
                    disabled={saving}
                    onClick={() => lifecycle('submitApproval')}
                  >
                    <Send className="mr-2 h-4 w-4" />
                    Submit approval
                  </Button>
                ) : null}
                {canApprove && worksheet.status === 'PendingApproval' ? (
                  <>
                    <Button
                      type="button"
                      className="bg-emerald-600 hover:bg-emerald-700"
                      disabled={saving}
                      onClick={() => lifecycle('approve')}
                    >
                      <CheckCircle2 className="mr-2 h-4 w-4" />
                      Approve
                    </Button>
                    <Button
                      type="button"
                      variant="destructive"
                      disabled={saving}
                      onClick={() => lifecycle('reject')}
                    >
                      <XCircle className="mr-2 h-4 w-4" />
                      Reject
                    </Button>
                  </>
                ) : null}
              </div>
            ) : null}
            {history.length > 0 ? (
              <div className="max-h-28 overflow-auto rounded-lg border p-2">
                <div className="mb-1 text-xs font-medium">
                  Immutable history
                </div>
                {history.map((item) => (
                  <div
                    key={item.id}
                    className="flex items-center gap-2 border-t py-1 text-xs"
                  >
                    <Badge variant="outline">{item.action}</Badge>
                    <span>{item.actorName}</span>
                    <span className="text-muted-foreground">
                      {new Date(item.createdAt).toLocaleString()}
                    </span>
                  </div>
                ))}
              </div>
            ) : null}
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
