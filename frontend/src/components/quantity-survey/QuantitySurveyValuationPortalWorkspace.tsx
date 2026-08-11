'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  CheckCircle2,
  Download,
  RefreshCw,
  Save,
  Send,
  Upload,
} from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
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
import {
  quantitySurveyValuationWorksheetService as service,
  type ValuationEvidenceType,
  type ValuationWorksheet,
  type ValuationWorksheetLine,
} from '@/services/quantity-survey-valuation-worksheet.service';

type Props = { projectId: string };
const quantity = (value: number) =>
  Number(value || 0).toLocaleString(undefined, { maximumFractionDigits: 4 });
const money = (value: number, currency: string) =>
  new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: currency || 'GHS',
    maximumFractionDigits: 2,
  }).format(value || 0);

export function QuantitySurveyValuationPortalWorkspace({ projectId }: Props) {
  const [items, setItems] = useState<ValuationWorksheet[]>([]);
  const [selected, setSelected] = useState<ValuationWorksheet>();
  const [lines, setLines] = useState<ValuationWorksheetLine[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [notes, setNotes] = useState('');
  const [evidenceFile, setEvidenceFile] = useState<File | null>(null);
  const [evidenceTitle, setEvidenceTitle] = useState('');
  const [evidenceType, setEvidenceType] =
    useState<ValuationEvidenceType>('ContractorClaim');
  const requests = useRef(new Map<string, string>());

  const load = useCallback(
    async (preferredId?: string) => {
      setLoading(true);
      try {
        const values = await service.externalList(projectId);
        setItems(values);
        const id = preferredId || values[0]?.id;
        const current = id
          ? await service.externalGet(projectId, id)
          : undefined;
        setSelected(current);
        setLines(current?.lines || []);
      } catch (error) {
        toast.error(
          error instanceof Error
            ? error.message
            : 'Valuations could not be loaded'
        );
      } finally {
        setLoading(false);
      }
    },
    [projectId]
  );

  useEffect(() => {
    void load();
  }, [load]);
  const currency = selected?.lines[0]?.currency || 'GHS';
  const currentRequest = (key: string) => {
    const existing = requests.current.get(key);
    if (existing) return existing;
    const created = crypto.randomUUID();
    requests.current.set(key, created);
    return created;
  };
  const complete = async (
    key: string,
    action: () => Promise<ValuationWorksheet>,
    message: string
  ) => {
    setBusy(true);
    try {
      const value = await action();
      requests.current.delete(key);
      setSelected(value);
      setLines(value.lines);
      setNotes('');
      toast.success(message);
      await load(value.id || undefined);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'The valuation action failed'
      );
    } finally {
      setBusy(false);
    }
  };

  const saveClaim = async () => {
    if (!selected?.id || !selected.rowVersion) return;
    const worksheetId = selected.id;
    const payload = {
      projectBoqVersionId: selected.projectBoqVersionId,
      contractorBusinessPartnerId: selected.contractorBusinessPartnerId,
      consultantBusinessPartnerId: selected.consultantBusinessPartnerId,
      rowVersion: selected.rowVersion,
      retentionPercentage: selected.retentionPercentage,
      lines: lines.map((line) => ({
        projectBoqVersionLineId: line.projectBoqVersionLineId,
        currentClaimedQuantity: line.currentClaimedQuantity,
        currentCertifiedQuantity: line.previouslyCertifiedQuantity,
        reviewNote: null,
      })),
    };
    const fingerprint = JSON.stringify(payload);
    await complete(
      `save:${worksheetId}:${fingerprint}`,
      () =>
        service.saveContractorClaim(projectId, worksheetId, {
          clientRequestId: currentRequest(`save:${worksheetId}:${fingerprint}`),
          ...payload,
        }),
      'Contractor claim saved.'
    );
  };

  const endorse = async (consultant: boolean) => {
    if (!selected?.id || !selected.rowVersion) return;
    const worksheetId = selected.id;
    const key = `${consultant ? 'consultant' : 'contractor'}:${worksheetId}`;
    const attestation = consultant
      ? selected.consultantAttestationText
      : selected.contractorAttestationText;
    await complete(
      key,
      () =>
        (consultant
          ? service.endorseConsultant
          : service.submitContractorClaim)(projectId, worksheetId, {
          clientRequestId: currentRequest(key),
          rowVersion: selected.rowVersion,
          notes: notes || undefined,
          signature: {
            method: 0,
            attestation,
            signedAt: new Date().toISOString(),
          },
        }),
      consultant
        ? 'Consultant endorsement recorded.'
        : 'Contractor claim submitted.'
    );
  };

  const uploadEvidence = async () => {
    if (!selected?.id || !evidenceFile || evidenceTitle.trim().length < 3) {
      toast.error('Select a file and enter an evidence title.');
      return;
    }
    setBusy(true);
    try {
      await service.addEvidence(
        projectId,
        selected.id,
        {
          clientRequestId: crypto.randomUUID(),
          evidenceType,
          title: evidenceTitle.trim(),
          file: evidenceFile,
        },
        true
      );
      setEvidenceFile(null);
      setEvidenceTitle('');
      await load(selected.id);
      toast.success('Evidence scanned and retained in the central DMS.');
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Evidence upload failed'
      );
    } finally {
      setBusy(false);
    }
  };

  const download = async (evidenceId: string, name: string) => {
    if (!selected?.id) return;
    try {
      const blob = await service.evidenceContent(
        projectId,
        selected.id,
        evidenceId,
        true
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = name;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Evidence could not be opened'
      );
    }
  };

  const selectableItems = useMemo(
    () => items.filter((item) => Boolean(item.id)),
    [items]
  );
  const totals = useMemo(
    () =>
      lines.reduce(
        (result, line) => ({
          claimed: result.claimed + line.currentClaimedQuantity * line.unitRate,
          measured: result.measured + line.measuredToDateValue,
        }),
        { claimed: 0, measured: 0 }
      ),
    [lines]
  );

  if (loading)
    return (
      <Card>
        <CardContent className="py-10 text-center text-sm text-muted-foreground">
          Loading governed interim valuations…
        </CardContent>
      </Card>
    );
  if (!items.length)
    return (
      <Card>
        <CardHeader>
          <CardTitle>Interim valuations</CardTitle>
          <CardDescription>
            No valuation has been assigned to your business-partner account.
          </CardDescription>
        </CardHeader>
      </Card>
    );
  if (!selected) return null;

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <CardTitle>Interim valuation claims</CardTitle>
              <CardDescription>
                Submit contractor claims or independently endorse QS-vetted
                valuations.
              </CardDescription>
            </div>
            <Button
              variant="outline"
              size="sm"
              onClick={() => load(selected.id || undefined)}
            >
              <RefreshCw className="mr-2 h-4 w-4" />
              Refresh
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="grid gap-3 md:grid-cols-[minmax(240px,1fr)_auto_auto]">
            <Select
              value={selected.id || ''}
              onValueChange={(value) => load(value)}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {selectableItems.map((item) => (
                  <SelectItem key={item.id} value={item.id || ''}>
                    {item.interimValuationLabel}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Badge className="w-fit">{selected.status}</Badge>
            {selected.certificateReady ? (
              <Badge className="w-fit bg-emerald-600">Certificate ready</Badge>
            ) : (
              <Badge variant="outline" className="w-fit">
                {selected.approvalStatus}
              </Badge>
            )}
          </div>
          <div className="flex flex-wrap gap-4 text-sm text-muted-foreground">
            <span>Contractor: {selected.contractorName || 'Not assigned'}</span>
            <span>Consultant: {selected.consultantName || 'Not assigned'}</span>
            <span>Claimed: {money(totals.claimed, currency)}</span>
            <span>Measured: {money(totals.measured, currency)}</span>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="overflow-auto p-0">
          <table className="min-w-[1050px] text-sm">
            <thead className="bg-muted">
              <tr>
                {[
                  'BoQ item',
                  'Measured',
                  'Previously certified',
                  'Current claimed',
                  'QS certified',
                  'Claim value',
                ].map((label) => (
                  <th
                    key={label}
                    className="px-3 py-2 text-left text-xs font-medium"
                  >
                    {label}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {lines.map((line) => (
                <tr key={line.projectBoqVersionLineId} className="border-t">
                  <td className="max-w-[320px] px-3 py-2">
                    <div className="font-medium">{line.label}</div>
                    <div className="truncate text-xs text-muted-foreground">
                      {line.description}
                    </div>
                  </td>
                  <td className="px-3 py-2">
                    {quantity(line.measuredToDateQuantity)} {line.unitOfMeasure}
                  </td>
                  <td className="px-3 py-2">
                    {quantity(line.previouslyCertifiedQuantity)}
                  </td>
                  <td className="w-44 px-3 py-2">
                    <Input
                      type="number"
                      aria-label={`Claim ${line.label}`}
                      min={line.previouslyCertifiedQuantity}
                      max={line.measuredToDateQuantity}
                      step="0.0001"
                      disabled={selected.status !== 'Draft'}
                      value={line.currentClaimedQuantity}
                      onChange={(event) =>
                        setLines((current) =>
                          current.map((item) =>
                            item.projectBoqVersionLineId ===
                            line.projectBoqVersionLineId
                              ? {
                                  ...item,
                                  currentClaimedQuantity: Number(
                                    event.target.value || 0
                                  ),
                                }
                              : item
                          )
                        )
                      }
                    />
                  </td>
                  <td className="px-3 py-2">
                    {quantity(line.currentCertifiedQuantity)}
                  </td>
                  <td className="px-3 py-2">
                    {money(
                      line.currentClaimedQuantity * line.unitRate,
                      currency
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </CardContent>
      </Card>

      {selected.status === 'Draft' ? (
        <div className="flex justify-end">
          <Button disabled={busy} onClick={saveClaim}>
            <Save className="mr-2 h-4 w-4" />
            Save contractor claim
          </Button>
        </div>
      ) : null}
      {!['PendingApproval', 'Approved', 'Rejected'].includes(
        selected.status
      ) ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Supporting evidence</CardTitle>
            <CardDescription>
              Files are centrally scanned and retained in the governed document
              repository.
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-3 lg:grid-cols-[180px_minmax(180px,1fr)_minmax(220px,1fr)_auto] lg:items-end">
            <div className="grid gap-1">
              <Label>Evidence type</Label>
              <Select
                value={evidenceType}
                onValueChange={(value) =>
                  setEvidenceType(value as ValuationEvidenceType)
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
                  ].map((value) => (
                    <SelectItem key={value} value={value}>
                      {value.replace(/([a-z])([A-Z])/g, '$1 $2')}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-1">
              <Label>Title</Label>
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
              variant="outline"
              disabled={busy || !evidenceFile}
              onClick={uploadEvidence}
            >
              <Upload className="mr-2 h-4 w-4" />
              Upload
            </Button>
          </CardContent>
        </Card>
      ) : null}
      {selected.evidence.length ? (
        <div className="flex flex-wrap gap-2">
          {selected.evidence.map((item) => (
            <Button
              key={item.id}
              variant="outline"
              size="sm"
              onClick={() => download(item.id, item.originalFileName)}
            >
              <Download className="mr-2 h-3.5 w-3.5" />
              {item.title}
            </Button>
          ))}
        </div>
      ) : null}
      {selected.status === 'Draft' || selected.status === 'QsVetted' ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Attestation</CardTitle>
            <CardDescription>
              {selected.status === 'Draft'
                ? selected.contractorAttestationText
                : selected.consultantAttestationText}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="grid gap-1">
              <Label>Notes (optional)</Label>
              <Textarea
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
              />
            </div>
            <div className="flex justify-end">
              <Button
                disabled={busy}
                onClick={() => endorse(selected.status === 'QsVetted')}
              >
                {selected.status === 'Draft' ? (
                  <Send className="mr-2 h-4 w-4" />
                ) : (
                  <CheckCircle2 className="mr-2 h-4 w-4" />
                )}
                {selected.status === 'Draft'
                  ? 'Submit contractor claim'
                  : 'Endorse as consultant'}
              </Button>
            </div>
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}
